# PunchPoint — user guide

## Every day

**Employees clock in and out on the Clock screen.** They type their PIN and press
Clock In / Clock Out. While clocked in they can start an unpaid break; the button becomes
End Break.

The screen returns to the PIN pad by itself, so it can be left open all day.

## Adding an employee

Employees → **Add employee**.

* **PIN** — what the employee types on the clock. Must be unique. A suggested number is filled in.
* **Badge** — optional; if you have a badge reader, the badge number also works at the PIN pad.
* **Shift** — assigns break, grace, docking and holiday rules. Create shifts in Settings first.
* **Hire date** — used for holiday eligibility.
* **Clock message** — appears to the employee after they enter their PIN.

Deactivating an employee keeps their history and stops them clocking in. Deleting removes
their punches too — prefer deactivate for anyone who has left.

## Reviewing hours

**Time Cards** → choose a pay period at the top.

Left: every employee with regular, overtime, holiday and total hours. Orange `!` means the
system saw something it could not resolve — usually a missing punch.

Right: the selected employee day by day. Double-click a day for the full explanation:
each IN/OUT pair, the break taken, and every adjustment in plain English
("Arrived 6 min late but inside the 5-minute grace period", "Rounded to 15-minute interval",
"Automatic unpaid lunch (30 min)").

Corrections:

* **Add punch** — a missed punch, or vacation/holiday/sick hours.
* **Edit this day's punches** — from the day detail window.
* **Adjust hours** — a permanent +/- adjustment in minutes for this time card.
* **Approve time card** — marks it reviewed.

Punches can also be edited directly on the **Punches** screen, filtered by date and employee.

## Pay periods

Settings → **Pay periods**. Choose weekly, bi-weekly, semi-monthly or monthly, and the date a
period starts. **Create periods for 1 year back and forward** fills in the calendar. Creating
periods never changes punches.

If you import from timeQplus, the old period boundaries are reproduced, and the period type
is taken from the old data.

## Overtime and rounding

Settings → **Rounding and overtime**.

* **Round punches to (minutes)** — 0 turns rounding off. Modes: nearest, always later,
  always earlier.
* **Daily overtime after (hours)** — for example 8. Anything beyond that in one work day is OT1.
* **Daily double-time after** — optional second daily band (for example 12).
* **Weekly overtime after** — for example 40 hours per 7-day block inside the period.
* **Hours after midnight belong to previous day** — set this for evening shifts that run past
  midnight (for example 4 hours), so a 10 pm–2 am shift stays one work day.

Overtime is recalculated from punches every time, using these rules. Daily bands are applied
first, then weekly ones, so the same minute is never counted twice.

## Shifts

Settings → **Shifts**. A shift holds the rules for a group of employees:

* start and end time (overnight shifts are supported);
* **grace** — a punch inside the grace period is treated as on time;
* **dock** — a punch outside grace is pulled back to the shift start / end;
* **lunch** — auto-deduct a fixed unpaid lunch after N hours when no break was punched,
  or leave it to the employee's break punches;
* **holiday pay** — hours paid for holidays on this shift.

## Holidays

Settings → **Paid holidays**. Add the date; employees on shifts with holiday pay receive the
configured hours for that day, and the day is marked green on the time card.

## Exports

Reports. All files open in Excel or Google Sheets.

* **Payroll summary** — one row per employee with regular, OT1, OT2, holiday and total hours.
* **Daily detail** — one row per employee per day, with IN/OUT times and notes.
* **All punches** — the raw events, for auditing or another system.
* **Print all time cards** — one page per employee with signature lines.

## Supervisor PIN

Settings → Company → **Supervisor PIN**. When set, opening Employees, Time Cards, Punches,
Import, Reports or Settings asks for it. Leave it blank for no restriction. The Clock screen
is never locked, so employees can always clock in.

## Backups

A copy of the database is written to `C:\ProgramData\PunchPoint\Backups\` once a day and
before every import. Copies older than 30 days are removed.

* **Back up now** — an immediate copy.
* **Open data folder** — to copy the database somewhere else.
* **Restore from backup…** — replaces the current data with a chosen backup. A safety copy
  of the current data is made first.

To move PunchPoint to another PC, install it there and copy the contents of
`C:\ProgramData\PunchPoint\` (with PunchPoint closed).

## Troubleshooting

**"PIN not recognised"** — the PIN belongs to an inactive employee, or it was typed with
extra characters. Check Employees.

**A day is orange** — a punch is missing. Open the day, press Add punch, add the missing
IN or OUT, then approve the time card.

**Nothing appears on the clock screen** — no active employees, or the app is open on
another user session. Only one instance can run at a time.

**Windows says the app is from an unknown publisher** — the exe is not code-signed.
Choose *More info* → *Run anyway*. Signing is only needed to avoid that prompt.

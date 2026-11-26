# PunchPoint — user guide

## First run

PunchPoint is two programs:

* **PunchPointClock.exe** — the employee clock. PIN pad only, and by default an ordinary
  window so the PC can be used for other work at the same time. On a PC used only for
  clocking in, set Settings → Company → **Clock window** to *Full screen kiosk*: it then
  covers the screen, stays on top, and cannot be closed without the password. Either way,
  **Ctrl+Shift+E** asks for the password, as does the window's close button (or, in kiosk
  mode, the **Supervisor** button in the top-right corner).

  The windowed clock can be resized by dragging its edge, down to 640×480, and it remembers
  the size you leave it at. Settings → Company → **Clock window size** has ready-made sizes
  (Compact 640×480 up to Extra large 1440×900) that apply to a running clock at once. Below about 760 px wide it rearranges into a single column — clock
  on top, PIN pad below — and hides the recent-activity list so the keys stay large enough to
  tap.
* **PunchPoint.exe** — the management app. It asks for the supervisor password every time
  it starts, and creates one on first run.

Keep both files in the same folder. In the admin app, **Open clock** starts the kiosk, and
Settings → *Start the clock automatically* makes Windows launch the clock at sign-in.

Leave the clock running on the work PC. Employees cannot reach employees, time cards,
imports or settings from it — those only exist in the password-protected app.

## Installing on several PCs (unattended)

The setup can run with no windows or prompts, which is handy for a lab, a scripted rollout, or
a remote session:

```
PunchPoint-Setup-1.6.8.exe /S /LOG=C:\Temp\punchpoint-setup.log
```

| Switch | Meaning |
|---|---|
| `/S` | Silent: no windows, no prompts |
| `/D=C:\Path` | Install folder (default `C:\Program Files\PunchPoint`) |
| `/LOG=C:\path` | Where to write the log (default `%TEMP%\PunchPoint-Setup.log`) |
| `/NODESKTOP` | Do not create desktop shortcuts |
| `/AUTOSTART` | Start the employee clock when the user signs in |
| `/START` | Open PunchPoint when setup finishes |

Exit code **0** means success, **1** a failed install (the log says why) and **2** that it was not
run as administrator. Silent installs never touch `C:\ProgramData\PunchPoint`, so existing time
records are safe, exactly like the normal installer.

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

## Importing

Import → **Import the whole Data folder (all history)…** and choose
`C:\ProgramData\Acroprint\timeQplusV3`. Everything in `Archives` is imported as well, so
former employees and their punches come across too. Use *Show inactive* on the Employees
screen to see people who no longer work here.

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

## Appearance

Settings → Company → **Dark mode**: *Follow Windows* (default), *Off — light theme* or
*On — dark theme*. Off keeps the app light even when Windows is dark. Changing it asks you to
restart PunchPoint, and the employee clock picks it up the next time it starts.

In dark mode the window frame, drop-downs, lists, cards and grids all follow the palette.
A few controls are drawn by Windows itself (the calendar on a date field and the scroll bars)
and keep the system colours. The dashboard always shows totals in the format chosen under
**Totals shown as** — hours and minutes (`8:30`) unless you pick hundredths (`8.50`).

## Pay periods in drop-downs

Every period list marks the one containing today as **Current**, the period before it as
**Previous** and the one after it as **Next**, for example:

```
Current · Nov 18 – Nov 24, 2026
Previous · Nov 11 – Nov 17, 2026
Nov 4 – Nov 10, 2026
Oct 28 – Nov 3, 2026
```

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

## PDF reports

Reports → choose any report → **PDF**. The time card, hours summary, approvals and payroll
summary all print to a proper PDF with page numbers, using the same hours format as the screen.

## Emailing the summary at the end of a pay period

Settings → **Email reports**:

* **SMTP server / port** — e.g. `smtp.gmail.com`, port `587` with SSL/TLS on.
* **Username** and **password** — for Gmail/Outlook use an **app password**, not your normal
  password (Google: Account → Security → 2-Step Verification → App passwords).
* **Send from** — the address the mail comes from (usually the same account).
* **Send summaries to** — where the payroll summary should arrive.
* Optionally tick **Encrypt stored passwords with Windows DPAPI** so the SMTP password
  cannot be read from the database by another Windows account.
* Tick **Email a summary automatically when a pay period ends**.
* Optionally tick **Also email each employee their own time card** — this needs an email
  address on each employee (Employees → Edit → Email).

Press **Send test email** to prove the settings, then Save.

Once a period has ended, the next time the clock (or the admin app) is running, PunchPoint
emails one PDF with every employee's regular, overtime and holiday hours. Each period is only
sent once — the send is recorded in the database.

### Checking hours away from the shop

There is no web server in PunchPoint, so nothing is ever listening on your network. Remote
checking happens through the **emailed summary**: once a pay period ends, the PDF arrives in
your inbox (and optionally in each employee's).

If you later want a live view from home, the safe way to add it is a private mesh such as
Tailscale — no ports opened, encrypted end to end.

## Attendance

Reports → **Attendance** shows punctuality for the period: work days, days late, how many
minutes late in total, early departures and days with no punches. Weekends and paid holidays
are skipped. Employees without an assigned shift are measured against 08:00–17:00.

## Backups

Settings → Your data:

* **Keep the last N backups** — older ones are removed automatically (default 14).
* **Copy backups to** — pick a folder inside Google Drive, OneDrive or Dropbox (or a network
  share). Every backup is copied there too; the cloud client uploads it. No account details are
  stored in PunchPoint.
* **Activity log…** — who changed what, and when.

A backup is taken automatically once a day by whichever program (clock or admin app) starts
first. **Back up now** still works at any time.

## The clock's exit password

Settings → Company → **Closing the clock needs the supervisor password**. When it is on,
Ctrl+Shift+E or the Supervisor button asks for the password. When it is off, closing asks only
for a confirmation — useful when the supervisor is the one using that PC.

## Updates

Settings → **Updates** shows the version you are running. Once a day, when PunchPoint opens, it
asks GitHub whether a newer release exists; if there is one, a small window shows the release
notes with a **Download** button that opens the release page in your browser. Nothing is
downloaded or installed without you.

* **Check now** — ask immediately.
* **Skip this version** (in the update window) — stop being reminded about that release.
* **Releases from** — the GitHub repository, in `owner/name` form. Change it if you use your own
  fork. The check is one anonymous HTTPS request; no usage data is sent.

You can turn the automatic check off with the checkbox; manual checking still works.

## Exports

Reports. All files open in Excel or Google Sheets.

* **Payroll summary** — one row per employee with regular, OT1, OT2, holiday and total hours.
* **Daily detail** — one row per employee per day, with IN/OUT times and notes.
* **All punches** — the raw events, for auditing or another system.
* **Print all time cards** — one page per employee with signature lines.

## Supervisor password

Settings → Company → **Change supervisor password**. The whole management app is locked
behind this password, and the clock uses it to allow closing. If you ever forget it, restore
a backup from before it was set (or contact whoever keeps the backups) — it cannot be read
out of the database.

Closing the clock: press **Ctrl+Shift+E**, or click **Supervisor** in the bottom-right
corner, then enter the password.

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

# Changelog

What changed in each release, in plain language. Update checks read the newest **published**
release, so a version stays a draft here until it has been tested on Windows.

## 1.6.9 — 2026-11-27

**Employees can check their own punches.**

- After entering their PIN, the clock shows a **My punches** button beside *Not me*. It opens
  the current pay period for that employee: each day's first clock-in, last clock-out, break
  time and daily total, the period total, and whether their time card has been approved.
- A session still running shows **on the clock** instead of a clock-out time.
- Days that carry only paid hours say so (**Wed Nov 25 · holiday**), instead of showing an amount
  with no times next to it.
- If a supervisor used *Edit totals*, the footer says how much was added or removed, so the
  total always explains itself.
- Long names shrink to fit the card instead of being cut off.
- It is read-only (no supervisor password), and it closes itself after a minute so an employee
  who walks away cannot leave their card open for the next person.
- `PunchPointClock.exe --week <PIN>` opens the same view directly, which is handy for support.
- **Kiosk mode:** the week view, the Supervisor menu and the password prompt now appear *above*
  the full-screen clock. They used to open behind it, so the screen looked frozen.

**Punch Editing keeps your place.**

- Adding, editing or deleting a punch used to jump the screen back to the first employee in the
  list and the current period. It now stays on the employee and period you were working on.
- Reports and Export keep their period (and Reports its employee filter) when the data refreshes.

## 1.6.8 — 2026-11-26

**Punch direction repairs itself when a punch is added.**

- When someone forgets to clock in and presses the clock at the end of the day, the clock has to
  record an IN — it cannot know an OUT was meant. Adding the missing punch now **re-types the
  rest of that day** so it alternates IN, OUT, IN, OUT. The same happens for two OUTs, and when
  a punch is edited or deleted. Before this, the extra punch stayed wrong and the day's hours
  were silently lost.
- **Punch Editing → Fix in/out** repairs a whole period at once for the selected employee, after
  asking. It only changes punches that break the alternation, so a clean period is untouched,
  and it lists every change it makes. Break punches and paid-hours entries are never touched.
- The status line at the bottom of Punch Editing reports what was corrected
  ("re-typed 4:59 PM IN → OUT"), and each repair is written to the activity log.
- For scripts: `punchpoint fix-sequence [--db file] [--employee PIN] [--from d] [--to d] [--apply]`
  shows what would change, and writes only with `--apply`.

**Unattended install.**

- The setup can now run without any windows or prompts, which is what you want for installing
  on several PCs from a script, or for automated testing:
  `PunchPoint-Setup-1.6.8.exe /S` installs to `C:\Program Files\PunchPoint` with desktop
  shortcuts and no autostart.
- Options: `/D=C:\Path` install folder, `/LOG=C:\path` log file (default
  `%TEMP%\PunchPoint-Setup.log`), `/NODESKTOP` no shortcuts, `/AUTOSTART` start the clock at
  sign-in, `/START` open PunchPoint afterwards.
- It returns exit code 0 on success, 1 on failure and 2 when it is not running as
  administrator, and the log records the installed versions. Interactive installs are unchanged.

## 1.6.7 — 2026-11-24

**Pick the clock's window size, and open PunchPoint from the clock.**

- **Settings → Company → Clock window size** offers Compact (640×480), Small (800×600),
  Standard (1000×720), Large (1200×800) and Extra large (1440×900). Choosing one resizes a
  running clock within a few seconds, so you can see the result straight away. Dragging the
  clock's edge still works and shows as **Custom** with the size you chose.
- **Supervisor → Open PunchPoint** starts the management app from the clock and closes the
  clock; start the clock again from PunchPoint when you are done. You type the password once —
  the clock passes the app a one-time token instead of asking a second time. The chooser is also
  on **Ctrl+Shift+E**, and its other button closes the clock as before.
- The clock **no longer takes the keyboard from other programs**. On a shared PC it used to put
  the cursor back in its PIN box every second, so typing in Word or a browser went into the
  clock — and four digits could clock someone in or out. It now only takes the keyboard back
  when it is the window in use.
- The clock's digits now shrink to fit the card instead of being clipped in a narrow or short
  window.

## 1.6.6 — 2026-11-24

**The clock fits a small window.**

- The windowed clock can now be dragged down to **640×480** (it used to stop at 820×620), and
  it still remembers the size you leave it at.
- Below about 760 px wide it rearranges itself into **one column** — clock on top, PIN pad
  underneath — and hides the recent-activity list so the keypad keeps its room.
- The clock text, PIN box and keypad keys **scale with the window**, so nothing is clipped at
  any size. The keypad fills the space it is given instead of a fixed 320 px block.
- Windowed mode no longer shows the small **Supervisor** link in the corner: the window's own
  close button (or Alt+F4) asks for the password. Kiosk mode keeps the link.
- `PunchPointClock.exe --windowed --size 640x480` opens at an exact size, which makes trying a
  small window easy.

## 1.6.5 — 2026-11-24

**Fixes in the period lists.**

- **"Current" now marks the right period.** After an import two periods could cover the same
  day, and the label was attached to whichever came first in the list — so the week you were in
  showed no "Current" while "Previous" appeared one week too late. The label now follows the
  period the app treats as current.
- **The drop-down stops after next period.** It used to list every period generated ahead
  (three months of future weeks). Now it shows your history, the current period and the next
  one; later periods still exist for assignment but are not offered in the picker.
- Periods left over from an earlier start date no longer appear in the lists at all.

## 1.6.4 — 2026-11-24

**Fix: pay periods could be off by a day after an import.** If a Wednesday-to-Tuesday shop
imported its history, the app had already created a few periods of its own (anchored on the day
it was installed), and both grids then covered the same dates. The lookup took the latest start,
so the current period could begin a day late — and the punch list could be split between two
periods.

- **The period that matches your settings always wins**, and punches are re-pointed to it, even
  when leftover periods exist. Nothing is deleted: an approved time card is never disturbed.
- **Settings shows where today falls.** Under *A period starts on* there is now a line such as
  *"Today (Tue Nov 24) is in Wed Nov 18 – Tue Nov 24, 2026 · new period every week"*, so the
  anchor date no longer has to be interpreted in your head.
- Changing the start date or period length rebuilds the grid immediately, without a restart.
- Existing databases repair themselves the first time 1.6.4 starts.

## 1.6.3 — 2026-11-24

**Maintenance release.** Two fixes found while preparing the project screenshots.

- **Report tables show their content.** Columns size to what is in them, so dates read
  `11/23/2026` instead of `11/2…`, and times are readable; a long report scrolls rather than
  squeezing every column.
- **The dashboard no longer opens with its first row highlighted**, which looked like a
  selection and could be mistaken for data.

## 1.6.2 — 2026-11-24

**Feature release.** Punctuality reporting, a visible activity history and automatic backups
with an optional cloud copy — plus an optional password on closing the clock.

### What's new

- **Attendance report.** A new report shows, per employee: work days, days late, total minutes
  late, early departures, and days with no punches at all. Weekends and holidays are skipped.
  Punctuality needs a schedule: employees with no shift assigned show "—" for late/early
  (absences are still counted) instead of being measured against a guessed 08:00–17:00.
- **Activity log.** Settings → Your data → **Activity log…** shows the last 500 recorded
  changes: imports, punch edits, approvals, settings, backups — with the user and the time.
- **Backups on autopilot.** One backup is taken each day automatically (whichever program runs
  first), the newest **N** are kept (default 14), and an optional **copy folder** receives every
  backup. Point that folder at Google Drive, OneDrive, Dropbox or a network share and the cloud
  app does the rest — no accounts or tokens in PunchPoint.
- **The clock's exit password is now optional.** Settings → Company → *Closing the clock needs
  the supervisor password* can be switched off; closing then asks only for a confirmation.

### Notes

- The attendance report only counts days that have happened: weekdays still to come in the
  current period are not treated as absences.
- Database schema is version 11; existing data is migrated automatically and nothing is
  removed. The diagnostic CLI gained `report attendance|hours|approvals` and
  `demo --today` for repeatable checks.
- Backups stay ordinary SQLite files, so the copies in your cloud folder are readable by the
  same version of PunchPoint (or any SQLite tool).

## 1.5.3 — 2026-11-24

**Appearance and reliability update.** The dark theme keeps the card shading from the design
exploration, and installing no longer leaves the data folder writable only by administrators.

### What's new

- **Dark-mode shading.** Cards now carry a hairline border and softly rounded corners, and
  tables use alternating row shading, so each panel reads as its own surface instead of one
  flat sheet. Light mode gets the same subtle edges; nothing else about the screens changes —
  the layout, wording and controls are the 1.5.2 interface.

### Fixes

- **Installing no longer leaves the data folder admin-only.** The setup program started the
  supervisor app with its own administrator rights, so the app created the database as
  Administrator and the employee clock (running as the user) could fail with
  *"unable to open database file"*. The app now launches as the signed-in user, and the
  installer repairs the permissions of an existing `C:\ProgramData\PunchPoint` folder.

## 1.5.2 — 2026-11-23

**Maintenance release.** Fixes the one cosmetic problem left after the icon change: Windows
kept showing the previous icon after an upgrade.

- **The new icon appears immediately.** The installer now tells Windows that the icons changed
  and rebuilds its icon cache before it finishes, so the Start Menu, desktop shortcuts and the
  window title bar all show the pin-and-clock artwork straight away. Before this, the old icon
  could linger until the cache expired.
- No changes to time records, settings or the database.

## 1.5.1 — 2026-11-23

**Maintenance release.** Corrects the wording of the update message.

- Version numbers in the update check read `1.5.1` instead of `1.5.1.0`, matching the version
  shown in the app and on this page.
- Everything else is unchanged from 1.5.0.

## 1.5.0 — 2026-11-23

**Feature release.** Gives PunchPoint its own identity and a way to tell you when there is a
newer version.

### What's new

- **New application icon.** A navy location pin holding a clock face, used by both programs,
  the installer, the Start Menu shortcuts and the window title bar.
- **Check for updates.** Once a day, when the supervisor app opens, PunchPoint asks GitHub
  whether a newer release exists. If there is one it shows the release notes with a
  **Download** button that opens this page — and a **Skip this version** option for releases you
  do not want. Nothing is downloaded or installed by itself.
  **Settings → Updates** shows the version you are running, lets you check immediately, and can
  turn the daily check off.
- **Automatic publishing.** Pushing a version tag makes GitHub Actions run the tests, build the
  x64 and ARM64 programs, build the installer and attach everything to the release.

## 1.4.0 — 2026-11-23

- Removed the read-only web page; remote checking is the emailed summary.
- **Dark mode** is an explicit setting: Follow Windows / Off (light) / On (dark).
- The employee clock uses the same setting.

## 1.3.0 — 2026-11-23

- Optional Windows DPAPI encryption for the stored SMTP password.
- Read-only network page on the local network (removed again in 1.4.0).

## 1.2.0 — 2026-11-23

- PDF exports built in (reports and payroll summary).
- Emailed payroll summary at the end of each pay period, optionally a time card per employee.

## 1.1.2 — 2026-11-23

- Hours display as `8:30` by default, including printed time cards.

## 1.1.1 — 2026-11-23

- Out-of-order break punches are prevented and reported in plain language.

## 1.1.0 — 2026-11-23

- Dark mode, and pay-period lists that mark **Current**, **Previous** and **Next**.

## 1.0.0 — 2026-11-23

- First release: employee clock, punch editing, reports, exports, imports from timeQplus.

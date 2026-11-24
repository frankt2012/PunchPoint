# Changelog

What changed in each release, in plain language. Update checks read the newest **published**
release, so a version stays a draft here until it has been tested on Windows.

## 1.6.0 — 2026-11-24

**Feature release.** Fills the gaps around missing punches, punctuality and backups, and makes
the clock's exit password optional.

### What's new

- **Fix missing punches.** Punch Editing has a **Fix missing…** button. It lists every day with
  an unmatched clock-in or clock-out, suggests a time for the missing punch (the shift's start
  or stop), and adds the ones you tick — with one entry in the activity log.
- **Attendance report.** A new report shows, per employee: work days, days late, total minutes
  late, early departures, and days with no punches at all. Weekends and holidays are skipped;
  employees without a shift are measured against 08:00–17:00 and marked as such.
- **Activity log.** Settings → Your data → **Activity log…** shows the last 500 recorded
  changes: imports, punch edits, approvals, settings, backups — with the user and the time.
- **Backups on autopilot.** One backup is taken each day automatically (whichever program runs
  first), the newest **N** are kept (default 14), and an optional **copy folder** receives every
  backup. Point that folder at Google Drive, OneDrive, Dropbox or a network share and the cloud
  app does the rest — no accounts or tokens in PunchPoint.
- **The clock's exit password is now optional.** Settings → Company → *Closing the clock needs
  the supervisor password* can be switched off; closing then asks only for a confirmation.

### Notes

- Database schema is version 11; existing data is migrated automatically and nothing is
  removed.
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

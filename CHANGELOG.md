# Changelog

What changed in each release, in plain language. Update checks read the newest **published**
release, so a version stays a draft here until it has been tested on Windows.

## Unreleased

- **Installing no longer leaves the data folder admin-only.** The setup program started the
  supervisor app with its own administrator rights, so the app created the database as
  Administrator and the employee clock (running as the user) could fail with
  *"unable to open database file"*. The app now launches as the signed-in user, and the
  installer repairs the permissions of an existing `C:\ProgramData\PunchPoint` folder.

## 1.5.2 — 2026-11-23

- The installer now refreshes the Windows icon cache, so upgrading shows the new icon straight
  away instead of keeping the old one.

## 1.5.1 — 2026-11-23

- Update messages read `1.5.0` rather than `1.5.0.0`.

## 1.5.0 — 2026-11-23

### What's new

- **New application icon**: navy pin with a clock face, used by the admin app, the clock and the
  installer.
- **Check for updates.** Once a day the admin app asks GitHub for a newer release and, when
  there is one, shows its notes with a Download button. **Settings → Updates** shows the
  version, lets you check immediately, and can skip a release. PunchPoint never installs
  anything by itself.
- **Publishing is automated**: pushing a `v*` tag builds both architectures on GitHub Actions
  and attaches the installers.

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

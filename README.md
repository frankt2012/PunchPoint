# PunchPoint

A modern replacement for **Acroprint timeQplus v3** on Windows 11 — a PC time clock
for small businesses, with a one-click import of your existing timeQplus employees
and punches.

![PunchPoint](docs/icon-preview.png)

## Why

timeQplus v3 (2012) depends on components Windows 11 no longer ships — 32-bit JET/MDAC
drivers, old VB6 runtime OCXs, Windows Help. On Windows 11 it installs but misbehaves,
and Acroprint no longer supports it.

PunchPoint is a standalone Windows app that:

* runs on Windows 11 (x64 and ARM64) with **no installs, no runtime, no database engine** —
  a single `PunchPoint.exe`;
* **imports your existing data** directly from `arx_active.mdb`, the timeQplus database,
  with no export step and no Access installed;
* keeps the workflow your employees already know: type a PIN, press Clock In / Clock Out;
* gives you honest, explainable time cards: every adjustment (rounding, grace, docking,
  lunch, overtime) is shown in plain English.

## Open source

PunchPoint is MIT licensed (see [LICENSE](LICENSE)) — it is your app to keep, modify and
share. It contains no code from the original timeQplus software: the Access reader and all
behaviour were written from scratch (see [NOTICE.md](NOTICE.md)).

## Like the old app, but modern

| timeQplus screen | PunchPoint |
|---|---|
| Home | **Dashboard** — who is on the clock, hours this period, punch problems |
| Employees | **Employees** — same fields (name, badge, PIN, number, shift, hire date) plus pay type |
| Messages | **Messages** — company message and per-employee messages |
| Setup | **Settings** — pay periods, rounding, overtime, day change, approvals, 7th-day OT |
| Shifts | **Settings → Shifts** — start/stop, round, grace, dock, lunch, holiday hours |
| Punch Editing | **Punch Editing** — employee + period, every punch with day totals, insert/delete/edit, totals, approve |
| Reports | **Reports** — ten reports with period and employee filters, preview, print, export |
| Export | **Export** — payroll, generic ASCII, daily detail, raw punches, QuickBooks-friendly |
| Terminal | **Import** — reads the old timeQplus database and archives |
| Employee Transaction | **PunchPointClock.exe** — a locked-down full-screen clock |

Reports can be saved as **PDF** (built in, no external tools) and the payroll summary can be
**emailed automatically at the end of each pay period** — useful when you want to check hours
from home without remote access to the PC.

Dark mode is built in (Settings → Appearance: *Follow Windows*, *Light* or *Dark*), and every
pay-period drop-down marks the **Current** period, the **Previous** one and the **Next** one,
with plain dates for everything older. Quality-of-life additions over the original: automatic daily and pre-import backups, import
that reads the old database directly (no Access/JET needed), one-click history import from
the Archives folder, plain-English explanations of every adjustment, password-protected
admin app, kiosk clock that employees cannot leave, CSV export everywhere, and a
self-contained executable with no runtime to install.

## What it does

| Area | |
|---|---|
| **Clock** | Big PIN pad, clock in/out, optional unpaid breaks, recent activity, auto-return |
| **Dashboard** | Who is on the clock now, hours this period, punch problems |
| **Employees** | Profiles, PIN/badge, shift assignment, pay type and rate, active/inactive |
| **Messages** | Company-wide and per-employee messages shown on the clock |
| **Punch Editing** | Per-employee, per-period punches, corrections, totals, approval |
| **Punches** | Every raw clock event, editable, with audit trail |
| **Import** | Reads timeQplus `arx_active.mdb` directly; previews before writing |
| **Reports** | Ten reports with preview, print and CSV export |
| **Export** | Payroll, generic ASCII, daily detail, punches, QuickBooks-friendly files, PDF |
| **Email** | Payroll summary PDF when a pay period ends, optionally a time card to each employee |
| **Settings** | Pay periods, rounding, overtime, day-change, shifts, holidays, appearance, backups |

## Two programs

| Program | Who uses it | What it can do |
|---|---|---|
| **PunchPointClock.exe** | Employees | The PIN clock. Opens as a normal window so the PC can still be used for other work, or in **full-screen kiosk mode** on a PC used only for clocking in. No menus, no employee list, no time cards; closing it needs the supervisor password. |
| **PunchPoint.exe** | Supervisors | Everything else: employees, time cards, punches, import, reports, settings. Asks for the supervisor password every time it starts. |

The password is stored as a PBKDF2 hash (never in plain text), and is created the first
time the admin app runs. The clock and the admin app share one database, so punches appear
in the admin app immediately (it refreshes every few seconds).

**Clock window mode** is a setting (Settings → Company): *Windowed* by default for a shared
PC, or *Full screen kiosk* (always on top, locked) for a dedicated clock PC. The clock can
also be started with `--kiosk` or `--windowed` to override it per shortcut.

Keep both `.exe` files in the same folder. **Open clock** in the admin app launches the
kiosk; *Settings → Start the clock automatically* makes the clock start with Windows.

## Which file to use

| Work PC | Build |
|---|---|
| Any normal Intel/AMD Windows 10 or 11 PC | `dist/win-x64/PunchPoint.exe` |
| Windows on ARM (Surface Pro X, Parallels/VMware ARM VM) | `dist/win-arm64/PunchPoint.exe` |

The x64 build is a native 64-bit Intel executable (PE32+ x86-64) with the x64 SQLite
library embedded — no emulation, no runtime install. The ARM build exists only for
ARM-based Windows machines; an ARM PC can also run the x64 build under Windows' built-in
emulation if you prefer one file for everyone.

## Install (recommended)

Download **`PunchPoint-Setup-x.y.z.exe`** from [Releases](../../releases) and run it.
It is a normal Windows installer:

* installs to `C:\Program Files\PunchPoint` with a UAC prompt;
* adds **PunchPoint** and **Employee Clock** to the Start Menu (and optionally the Desktop);
* appears in *Settings → Apps* with a working uninstaller that asks whether to keep your data;
* about 90 MB, self-contained — no .NET runtime to install.

Portable builds (`PunchPoint-x.y.z-win-x64.zip`, `…-win-arm64.zip`) are attached to the same
release if you prefer to copy two files onto a machine.

The installer is a native **64-bit** setup program, so it runs on Intel/AMD Windows and on
Windows on ARM under x64 emulation. It installs both programs, creates Start Menu and
optional desktop shortcuts, and registers a proper uninstaller that asks whether to keep
your time records.

## Build the installer yourself

```bash
./scripts/build-installer.sh 1.0.0
```

The script runs the tests, publishes both programs, packs them as the installer payload and
produces `dist/PunchPoint-Setup-1.0.0.exe`. Only the .NET SDK, `zip` and the repo are needed
— no Windows machine.

Pushing a tag (`git tag v1.0.0 && git push --tags`) makes GitHub Actions test the code,
publish for x64 and ARM64, build the installer and attach the installer plus both portable
zips to a release automatically.

## Run without installing

1. Copy `PunchPoint.exe` **and** `PunchPointClock.exe` to the work PC (for example `C:\PunchPoint`).
2. Double-click `PunchPoint.exe` and create the supervisor password when asked.
   No installer and no admin rights are required.
3. Your data lives in `C:\ProgramData\PunchPoint\`:
   * `punchpoint.db` — the database (SQLite)
   * `Backups\` — a copy is made once a day and before every import
   * `Logs\` — error logs, if anything ever goes wrong

To have the clock start automatically, tick **Start the clock automatically** in
Settings (it writes a per-user Run entry pointing at `PunchPointClock.exe`).

## Importing from timeQplus

1. Open **Import**.
2. Press **Find it** — PunchPoint looks for the data folder in the usual places:
   `C:\ProgramData\Acroprint\timeQplusV3\` (older installs: `C:\Program Files\Acroprint\timeQplusV3\`).
   If the old software is on another PC, copy its whole `timeQplusV3` folder to this PC first.
3. For your full history press **Import the whole Data folder (all history)** and choose that folder.
   PunchPoint then reads `arx_active.mdb` **and every weekly snapshot in `Archives\`**, oldest first —
   in a typical installation that is several years of punches in one step.
   (Choosing just `arx_active.mdb` imports only the current period.)
4. Check the preview: employee list, punch counts, date range, and the first-day punch sequence.
5. Press **Start import**. A backup is taken first, and running it twice is safe.

What is imported: employees (name, PIN, badge, number, hire date, custom fields),
all punches, shifts, pay periods, paid holidays, company overtime/rounding rules, and
time-card approvals. See [docs/import-notes.md](docs/import-notes.md) for the details,
including the one judgement call (punch direction) and how it is detected.

If you only have exported text files rather than the database, they can be imported too —
open **Import** and choose the CSV/TXT file.

## Differences from timeQplus

* No terminal/fingerprint hardware support — this build is for clocking in at the PC.
  Every employee gets a PIN and/or badge number, exactly like the PC screen in the old app.
* The old "supervisor" accounts are replaced by a single optional supervisor PIN
  (Settings → Company) that gates the management screens.
* Overtime is computed from your configured rules every time, rather than being stored;
  the old manual "carry" fields are not carried forward. Manual corrections are done with
  **Adjust hours** on the Time Cards screen.

## Building

Requires the .NET SDK 11 (or 10 with the target framework changed to `net10.0-windows`).
The app cross-compiles from macOS or Linux.

```bash
# tests
dotnet test

# a single self-contained exe for Windows x64
dotnet publish src/PunchPoint.App -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o dist/win-x64

# Windows on ARM
dotnet publish src/PunchPoint.App -c Release -r win-arm64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o dist/win-arm64

# command line tools (import, verify, demo)
dotnet run --project src/PunchPoint.Cli -- demo --db /tmp/demo.db
```

Or run `./build.sh` to produce both builds in `dist/`.

## Project layout

```
src/PunchPoint.Core    cross-platform engine: Jet 4 reader, time-card maths, storage, import, export
src/PunchPoint.App     WinForms UI (net11.0-windows)
src/PunchPoint.Cli     console tools: inspect/import/verify
tests/                 xUnit tests + a synthetic timeQplus database fixture
tools/                 fixture generator, icon generator, build script
docs/                  user guide, import notes, architecture
```

The Jet 4 reader is written from the published page layout (with reference to the
Apache-2.0 licensed Jackcess project); it deliberately avoids the Microsoft Access
Database Engine, which is exactly the dependency that breaks on modern Windows.

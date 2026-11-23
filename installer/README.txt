PunchPoint - time clock
=======================

Two programs are installed:

  PunchPoint.exe         Management app. Asks for the supervisor password.
  PunchPointClock.exe    Employee clock. Full screen, PIN pad only.

First run
  Start "PunchPoint" from the Start Menu. The first time, you will create the
  supervisor password. Write it down: it is stored only as a hash and cannot be
  recovered.

Import your old timeQplus data
  Import  ->  "Import the whole Data folder (all history)..."
  Choose  C:\ProgramData\Acroprint\timeQplusV3   (close the old program first)

Leave the clock running for employees
  Start "Employee Clock". To close it, press Ctrl+Shift+E or click "Supervisor"
  in the bottom-right corner, then enter the supervisor password.

Your data
  C:\ProgramData\PunchPoint\punchpoint.db    database
  C:\ProgramData\PunchPoint\Backups\         a backup is taken daily and before
                                             every import (kept 30 days)
  C:\ProgramData\PunchPoint\Logs\            error logs, if anything goes wrong

Uninstall
  Settings -> Apps -> PunchPoint -> Uninstall. You will be asked whether to keep
  your time records; choose No to delete them.

PunchPoint is MIT licensed. Source and releases on GitHub.

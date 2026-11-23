# Architecture

```
┌──────────────────────────────┐
│ PunchPoint.App (WinForms)    │  screens, dialogs, printing
│  net11.0-windows             │
└──────────────┬───────────────┘
               │
┌──────────────▼───────────────┐      ┌──────────────────────────┐
│ PunchPoint.Core              │      │ PunchPoint.Cli           │
│  net11.0, no Windows APIs    │◄─────│  inspect / import / demo │
│                              │      └──────────────────────────┘
│  Jet/      Jet 4 MDB reader  │
│  Import/   timeQplus import  │
│  TimeCalc/ time card engine  │
│  Storage/  SQLite + schema   │
│  Export/   CSV writers       │
└──────────────────────────────┘
```

Everything except the UI is plain .NET and runs on macOS/Linux, which is why the engine
has an extensive automated test suite and a CLI that can be used to check a real
database before trusting the import.

## Storage

SQLite through `Microsoft.Data.Sqlite` (bundled native library — no install, works on
x64 and ARM64). One file: `C:\ProgramData\PunchPoint\punchpoint.db`.

* WAL journal, `synchronous=FULL` — a power cut cannot corrupt the database.
* `schema_info.version` drives migrations; `Schema.Ensure` creates v1.
* Times are stored as local `yyyy-MM-dd HH:mm:ss` strings; dates as `yyyy-MM-dd`.
  Sorting and range queries work lexicographically, and the values are human-readable
  when someone opens the database with a SQLite browser.
* `audit_log` records the important actions (punch edits, approvals, imports).

## Time calculation

`TimeCardCalculator.Calculate` is a pure function: employee + shift + period + punches +
settings + holidays → `TimeCardResult`. No database access, which makes it directly
testable.

Order of operations:

1. **Pair punches** across the whole period, not per day, so overnight shifts work.
   A sequence is IN → (break out/in)* → OUT; unpaired punches become recorded issues
   rather than invented hours.
2. **File segments** under the work date of their IN punch, using the day-change offset.
3. **Automatic lunch** — only when the shift asks for it and no break was punched.
4. **Shift rules** — grace and docking, applied only when the punch is within 8 hours of
   the scheduled boundary (so a second shift is never docked to the first shift's start).
5. **Rounding** — per punch or per day total, with nearest/up/down modes.
6. **Daily overtime**, then **weekly overtime** in 7-day blocks from the period start.
   Double-time bands are handled after the first band so minutes are never counted twice.
7. **Holiday pay** and **miscellaneous hours** (vacation/sick) are added and excluded
   from overtime.

Every change is appended to `WorkSegment.Adjustments`, which is what the day-detail
dialog shows. A total can always be explained.

## Jet 4 reader

`Jet4Database` / `Jet4Table` implement just enough of the Microsoft Access Jet 4 format:

* file header validation (and a clear message for Access 97 files);
* the system catalog on page 2 (`MSysObjects`) to enumerate tables;
* table definition pages, including multi-page definitions;
* column descriptors: type, number, fixed offset, variable-table index, length, scale;
* usage maps — inline bitmaps and reference maps — to find the table's data pages;
* row pages, including deleted rows and overflow rows chained to other pages;
* value decoding: text (including the FF FE compressed-unicode form), memo long values
  (inline, single page, multi-page), int16/int32/int64, single/double, currency, numeric,
  GUID, boolean (from the null mask) and date/time.

Unsupported tables or columns never block a punch import: the importer reads the columns
it needs by name.

## Import pipeline

```
arx_active.mdb
   → Jet4Database (schema + rows)
   → LegacyEmployee / LegacyTransaction (raw, direction unknown)
   → PunchDirectionDetector (score candidate columns, else alternate)
   → mapping (employees, shifts, periods, holidays, settings)
   → SQLite (deduplicated by legacy punch id, inside one transaction)
```

The whole import is one transaction: either everything lands or nothing does. Punches keep
their legacy `TransactionID` so a second import skips what is already there.

## Tests

`tests/PunchPoint.Core.Tests` covers:

* Jet 4 reading of a synthetic timeQplus database (values, booleans, memos, dates, row counts);
* punch direction detection (binary codes, terminal break codes, alternation, resync);
* time card maths — 20+ scenarios covering lunch, missing punches, overnight, grace,
  docking, rounding modes, daily/weekly overtime, holidays, adjustments;
* pay period generation for all four period types;
* the importer end-to-end, including running twice and misc-hours handling.

The Jet reader was additionally checked against 32 Access databases (118 tables) from the
Jackcess test corpus, comparing every table's row count and sampled values with `mdbtools`.

using Microsoft.Data.Sqlite;

namespace PunchPoint.Core.Storage;

public static class Schema
{
    public const int CurrentVersion = 8;

    public static void Ensure(Database db)
    {
        int version = GetVersion(db);

        if (version < 1)
        {
            CreateV1(db);
            SetVersion(db, 1);
        }

        if (version < 2)
        {
            // The supervisor PIN (plaintext) became a hashed password.
            try
            {
                db.Execute("ALTER TABLE settings ADD COLUMN admin_password TEXT NOT NULL DEFAULT '';");
            }
            catch
            {
                // column already present
            }
            SetVersion(db, 2);
        }

        if (version < 3)
        {
            foreach (var sql in new[]
                     {
                         "ALTER TABLE settings ADD COLUMN totals_format INTEGER NOT NULL DEFAULT 0;",
                         "ALTER TABLE settings ADD COLUMN show_punch_comment INTEGER NOT NULL DEFAULT 1;",
                         "ALTER TABLE settings ADD COLUMN enable_7th_day_ot INTEGER NOT NULL DEFAULT 0;",
                     })
            {
                try { db.Execute(sql); } catch { /* already present */ }
            }
            SetVersion(db, 3);
        }

        if (version < 4)
        {
            foreach (var sql in new[]
                     {
                         "ALTER TABLE settings ADD COLUMN clock_fullscreen INTEGER NOT NULL DEFAULT 0;",
                         "ALTER TABLE settings ADD COLUMN clock_width INTEGER NOT NULL DEFAULT 1000;",
                         "ALTER TABLE settings ADD COLUMN clock_height INTEGER NOT NULL DEFAULT 720;",
                     })
            {
                try { db.Execute(sql); } catch { /* already present */ }
            }
            SetVersion(db, 4);
        }

        if (version < 5)
        {
            try { db.Execute("ALTER TABLE settings ADD COLUMN app_theme INTEGER NOT NULL DEFAULT 0;"); }
            catch { /* already present */ }
            SetVersion(db, 5);
        }

        if (version < 6)
        {
            // Display hours as 8:30 by default, as the original software did. The
            // Settings screen still offers hundredths (8.50) for anyone who prefers it.
            try { db.Execute("UPDATE settings SET totals_format = 0 WHERE totals_format = 1;"); }
            catch { /* nothing to migrate */ }
            SetVersion(db, 6);
        }

        if (version < 7)
        {
            foreach (var sql in new[]
                     {
                         "ALTER TABLE employees ADD COLUMN email TEXT NOT NULL DEFAULT '';",
                         "ALTER TABLE settings ADD COLUMN smtp_host TEXT NOT NULL DEFAULT '';",
                         "ALTER TABLE settings ADD COLUMN smtp_port INTEGER NOT NULL DEFAULT 587;",
                         "ALTER TABLE settings ADD COLUMN smtp_ssl INTEGER NOT NULL DEFAULT 1;",
                         "ALTER TABLE settings ADD COLUMN smtp_user TEXT NOT NULL DEFAULT '';",
                         "ALTER TABLE settings ADD COLUMN smtp_password TEXT NOT NULL DEFAULT '';",
                         "ALTER TABLE settings ADD COLUMN smtp_from TEXT NOT NULL DEFAULT '';",
                         "ALTER TABLE settings ADD COLUMN report_email TEXT NOT NULL DEFAULT '';",
                         "ALTER TABLE settings ADD COLUMN email_period INTEGER NOT NULL DEFAULT 0;",
                         "ALTER TABLE settings ADD COLUMN email_employees INTEGER NOT NULL DEFAULT 0;",
                     })
            {
                try { db.Execute(sql); } catch { /* already present */ }
            }

            db.Execute("""
                CREATE TABLE IF NOT EXISTS email_log (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    period_id   INTEGER NOT NULL,
                    kind        TEXT NOT NULL DEFAULT '',
                    sent_at     TEXT NOT NULL,
                    recipients  TEXT NOT NULL DEFAULT '',
                    note        TEXT NOT NULL DEFAULT ''
                );
                """);

            SetVersion(db, 7);
        }

        if (version < 8)
        {
            foreach (var sql in new[]
                     {
                         "ALTER TABLE settings ADD COLUMN encrypt_secrets INTEGER NOT NULL DEFAULT 0;",
                         "ALTER TABLE settings ADD COLUMN web_access INTEGER NOT NULL DEFAULT 0;",
                         "ALTER TABLE settings ADD COLUMN web_port INTEGER NOT NULL DEFAULT 8080;",
                         "ALTER TABLE settings ADD COLUMN web_user TEXT NOT NULL DEFAULT 'punchpoint';",
                         "ALTER TABLE settings ADD COLUMN web_password TEXT NOT NULL DEFAULT '';",
                     })
            {
                try { db.Execute(sql); } catch { /* already present */ }
            }
            SetVersion(db, 8);
        }
    }

    private static int GetVersion(Database db)
    {
        db.Execute("CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL);");
        object? value = db.Scalar("SELECT version FROM schema_info LIMIT 1;");
        if (value is null)
        {
            db.Execute("INSERT INTO schema_info (version) VALUES (0);");
            return 0;
        }
        return Convert.ToInt32(value);
    }

    private static void SetVersion(Database db, int version) =>
        db.Execute("UPDATE schema_info SET version = $v;", ("$v", version));

    private static void CreateV1(Database db)
    {
        db.Execute("""
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS settings (
                id                  INTEGER PRIMARY KEY CHECK (id = 1),
                company_name        TEXT NOT NULL DEFAULT '',
                company_message     TEXT NOT NULL DEFAULT '',
                admin_pin           TEXT NOT NULL DEFAULT '',
                admin_password      TEXT NOT NULL DEFAULT '',
                totals_format       INTEGER NOT NULL DEFAULT 0,
                show_punch_comment  INTEGER NOT NULL DEFAULT 1,
                enable_7th_day_ot   INTEGER NOT NULL DEFAULT 0,
                clock_fullscreen    INTEGER NOT NULL DEFAULT 0,
                clock_width         INTEGER NOT NULL DEFAULT 1000,
                clock_height        INTEGER NOT NULL DEFAULT 720,
                app_theme           INTEGER NOT NULL DEFAULT 0,
                period_type         INTEGER NOT NULL DEFAULT 0,
                period_anchor       TEXT NOT NULL,
                semi_monthly_split  INTEGER NOT NULL DEFAULT 16,
                monthly_start_day   INTEGER NOT NULL DEFAULT 1,
                rounding_minutes    INTEGER NOT NULL DEFAULT 0,
                round_in_mode       INTEGER NOT NULL DEFAULT 1,
                round_out_mode      INTEGER NOT NULL DEFAULT 1,
                daily_ot1           INTEGER NOT NULL DEFAULT 0,
                daily_ot2           INTEGER NOT NULL DEFAULT 0,
                weekly_ot1          INTEGER NOT NULL DEFAULT 0,
                weekly_ot2          INTEGER NOT NULL DEFAULT 0,
                day_change_offset   INTEGER NOT NULL DEFAULT 0,
                max_punch_span      INTEGER NOT NULL DEFAULT 960,
                require_approval    INTEGER NOT NULL DEFAULT 0,
                round_daily_total   INTEGER NOT NULL DEFAULT 0,
                time_format         TEXT NOT NULL DEFAULT 'h:mm tt',
                date_format         TEXT NOT NULL DEFAULT 'M/d/yyyy',
                last_backup_path    TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS employees (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                first_name      TEXT NOT NULL DEFAULT '',
                middle_initial  TEXT NOT NULL DEFAULT '',
                last_name       TEXT NOT NULL DEFAULT '',
                badge           TEXT NOT NULL DEFAULT '',
                pin             TEXT NOT NULL DEFAULT '',
                employee_number TEXT NOT NULL DEFAULT '',
                shift_id        INTEGER REFERENCES shifts(id) ON DELETE SET NULL,
                message         TEXT NOT NULL DEFAULT '',
                hire_date       TEXT NULL,
                export_id       TEXT NOT NULL DEFAULT '',
                email           TEXT NOT NULL DEFAULT '',
                status          INTEGER NOT NULL DEFAULT 0,
                pay_type        INTEGER NOT NULL DEFAULT 0,
                pay_rate        TEXT NULL,
                notes           TEXT NOT NULL DEFAULT '',
                custom1         TEXT NOT NULL DEFAULT '',
                custom2         TEXT NOT NULL DEFAULT '',
                custom3         TEXT NOT NULL DEFAULT '',
                custom4         TEXT NOT NULL DEFAULT '',
                custom5         TEXT NOT NULL DEFAULT '',
                legacy_id       INTEGER NULL,
                created_at      TEXT NOT NULL,
                updated_at      TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_employees_pin ON employees(pin) WHERE pin <> '';
            CREATE UNIQUE INDEX IF NOT EXISTS ix_employees_badge ON employees(badge) WHERE badge <> '';
            CREATE INDEX IF NOT EXISTS ix_employees_legacy ON employees(legacy_id);

            CREATE TABLE IF NOT EXISTS shifts (
                id                      INTEGER PRIMARY KEY AUTOINCREMENT,
                name                    TEXT NOT NULL DEFAULT '',
                enabled                 INTEGER NOT NULL DEFAULT 1,
                start_minutes           INTEGER NOT NULL DEFAULT 480,
                stop_minutes            INTEGER NOT NULL DEFAULT 1020,
                start_grace             INTEGER NOT NULL DEFAULT 0,
                stop_grace              INTEGER NOT NULL DEFAULT 0,
                dock_late               INTEGER NOT NULL DEFAULT 0,
                dock_early              INTEGER NOT NULL DEFAULT 0,
                lunch_enabled           INTEGER NOT NULL DEFAULT 0,
                lunch_start_minutes     INTEGER NOT NULL DEFAULT 720,
                lunch_end_minutes       INTEGER NOT NULL DEFAULT 750,
                lunch_auto_after        INTEGER NOT NULL DEFAULT 0,
                lunch_duration          INTEGER NOT NULL DEFAULT 30,
                holidays_paid           INTEGER NOT NULL DEFAULT 0,
                holiday_hours           INTEGER NOT NULL DEFAULT 0,
                legacy_id               INTEGER NULL
            );

            CREATE TABLE IF NOT EXISTS pay_periods (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                start_date  TEXT NOT NULL,
                end_date    TEXT NOT NULL,
                period_type INTEGER NOT NULL DEFAULT 0,
                split_day   INTEGER NOT NULL DEFAULT 15,
                is_closed   INTEGER NOT NULL DEFAULT 0
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_pay_periods_start ON pay_periods(start_date);

            CREATE TABLE IF NOT EXISTS punches (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                employee_id     INTEGER NOT NULL REFERENCES employees(id) ON DELETE CASCADE,
                pay_period_id   INTEGER NULL REFERENCES pay_periods(id) ON DELETE SET NULL,
                timestamp       TEXT NOT NULL,
                kind            INTEGER NOT NULL DEFAULT 0,
                source          INTEGER NOT NULL DEFAULT 0,
                comment         TEXT NOT NULL DEFAULT '',
                is_voided       INTEGER NOT NULL DEFAULT 0,
                hours_minutes   INTEGER NOT NULL DEFAULT 0,
                hours_category  TEXT NOT NULL DEFAULT '',
                legacy_id       INTEGER NULL,
                created_at      TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_punches_employee_time ON punches(employee_id, timestamp);
            CREATE INDEX IF NOT EXISTS ix_punches_time ON punches(timestamp);
            CREATE INDEX IF NOT EXISTS ix_punches_legacy ON punches(legacy_id);

            CREATE TABLE IF NOT EXISTS time_cards (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                employee_id         INTEGER NOT NULL REFERENCES employees(id) ON DELETE CASCADE,
                pay_period_id       INTEGER NOT NULL REFERENCES pay_periods(id) ON DELETE CASCADE,
                approved            INTEGER NOT NULL DEFAULT 0,
                approved_by         TEXT NOT NULL DEFAULT '',
                approved_at         TEXT NULL,
                adjust_reg_minutes  INTEGER NOT NULL DEFAULT 0,
                adjust_ot1_minutes  INTEGER NOT NULL DEFAULT 0,
                adjust_ot2_minutes  INTEGER NOT NULL DEFAULT 0,
                note                TEXT NOT NULL DEFAULT ''
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_time_cards_unique ON time_cards(employee_id, pay_period_id);

            CREATE TABLE IF NOT EXISTS holidays (
                id      INTEGER PRIMARY KEY AUTOINCREMENT,
                name    TEXT NOT NULL DEFAULT '',
                date    TEXT NOT NULL,
                paid    INTEGER NOT NULL DEFAULT 1
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_holidays_date ON holidays(date);

            CREATE TABLE IF NOT EXISTS import_logs (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                source      TEXT NOT NULL,
                imported_at TEXT NOT NULL,
                summary     TEXT NOT NULL,
                details     TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS audit_log (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                happened_at TEXT NOT NULL,
                user        TEXT NOT NULL DEFAULT '',
                action      TEXT NOT NULL,
                detail      TEXT NOT NULL DEFAULT ''
            );
            """);

        db.Execute("""
            INSERT INTO settings (id, company_name, company_message, period_type, period_anchor)
            VALUES (1, 'My Company', '', 0, $anchor);
            """, ("$anchor", DateTime.Today.ToString("yyyy-MM-dd")));
    }
}

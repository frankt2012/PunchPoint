using PunchPoint.Core.Jet;
using PunchPoint.Core.Models;

namespace PunchPoint.Core.Import;

/// <summary>A transaction read from the legacy database, before direction is inferred.</summary>
public sealed class LegacyTransaction
{
    public int LegacyId { get; init; }
    public DateTime Timestamp { get; init; }
    public string Badge { get; init; } = "";
    public int Type { get; init; }
    public int Source { get; init; }
    public int State { get; init; }
    public int Hours { get; init; }
    public string Comment { get; init; } = "";
    public int? TimeCardId { get; init; }

    public PunchKind Kind { get; set; } = PunchKind.In;

    /// <summary>Employee resolved from the TimeCards link (or badge) before pairing.</summary>
    public int EmployeeId { get; set; } = -1;
}

/// <summary>An employee row from the legacy database plus its unchanged shift reference.</summary>
public sealed class LegacyEmployee
{
    public required Employee Employee { get; init; }
    public int? LegacyShiftId { get; init; }
    public int LegacyStatus { get; init; }
}

/// <summary>
/// Reads an Acroprint timeQplus v3 database (arx_active.mdb) and imports
/// employees, punches, shifts, pay periods, holidays and company settings.
/// Pure managed code: no Access/JET driver required.
/// </summary>
public sealed class TimeQPlusImporter
{
    private readonly string _path;

    public TimeQPlusImporter(string mdbPath) => _path = mdbPath;

    public static bool LooksLikeTimeQPlusDatabase(string path)
    {
        try
        {
            using var db = Jet4Database.Open(path);
            return db.HasTable("Employees") && db.HasTable("Transactions");
        }
        catch
        {
            return false;
        }
    }

    // =================================================================
    // Preview
    // =================================================================

    public ImportPreview Preview(ImportOptions? options = null)
    {
        options ??= new ImportOptions();
        using var db = Jet4Database.Open(_path);

        var preview = new ImportPreview { SourcePath = Path.GetFullPath(_path) };

        foreach (string name in new[] { "Employees", "Transactions", "Shifts", "PayPeriods", "Holidays", "Setup", "TimeCards" })
        {
            if (db.HasTable(name)) preview.TablesFound.Add(name);
            else preview.TablesMissing.Add(name);
        }

        if (!db.HasTable("Employees") || !db.HasTable("Transactions"))
        {
            preview.Warnings.Add("This does not look like a timeQplus database: the Employees and Transactions tables are missing.");
            return preview;
        }

        var employees = ReadEmployees(db);
        preview.EmployeeCount = employees.Count;

        var transactions = ReadTransactions(db);
        preview.TransactionCount = transactions.Count;

        if (transactions.Count > 0)
        {
            preview.FirstPunch = transactions.Min(t => t.Timestamp);
            preview.LastPunch = transactions.Max(t => t.Timestamp);
        }

        preview.MiscHourTransactions = transactions.Count(t => t.Hours > 0);
        preview.MiscHoursTotal = transactions.Where(t => t.Hours > 0).Sum(t => t.Hours) / 60.0;

        preview.ShiftCount = SafeCount(db, "Shifts");
        preview.PayPeriodCount = SafeCount(db, "PayPeriods");
        preview.HolidayCount = SafeCount(db, "Holidays");

        var lookup = BuildEmployeeLookup(employees);
        var timeCardMap = BuildTimeCardEmployeeMap(db);
        var known = employees.Where(e => e.Employee.LegacyId is not null)
            .Select(e => e.Employee.LegacyId!.Value).ToHashSet();
        ResolveEmployees(transactions, lookup, timeCardMap, known);

        var detection = PunchDirectionDetector.Detect(transactions, options.DirectionMode);
        preview.DetectedDirection = detection.Description;
        preview.DirectionConfidence = detection.Confidence;

        PunchDirectionDetector.Assign(transactions, detection);

        var byEmployee = transactions
            .Where(t => t.Kind is PunchKind.In or PunchKind.Out or PunchKind.BreakIn or PunchKind.BreakOut)
            .Where(t => t.EmployeeId >= 0)
            .GroupBy(t => t.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Timestamp).ToList());

        foreach (var legacy in employees)
        {
            var row = new EmployeeImportRow { Employee = legacy.Employee };
            int id = legacy.Employee.LegacyId ?? -1;

            if (byEmployee.TryGetValue(id, out var list) && list.Count > 0)
            {
                row.PunchCount = list.Count;
                row.FirstPunch = list[0].Timestamp;
                row.LastPunch = list[^1].Timestamp;
                row.OddPunchDays = list.GroupBy(t => t.Timestamp.Date).Count(g => g.Count() % 2 != 0);

                var firstDay = list.GroupBy(t => t.Timestamp.Date).OrderBy(g => g.Key).First();
                row.FirstDaySummary =
                    $"{firstDay.Key:ddd M/d}: " +
                    string.Join(", ", firstDay.OrderBy(t => t.Timestamp)
                        .Take(8)
                        .Select(t => $"{t.Timestamp:h:mm tt} {ShortKind(t.Kind)}"));

                if (row.OddPunchDays > 0)
                    row.Notes.Add($"{row.OddPunchDays} day(s) with an odd number of punches (a missing punch).");
                if (list.Count % 2 != 0)
                    row.Notes.Add("The most recent punch has no partner.");
            }
            else
            {
                row.Notes.Add("No punches found.");
            }

            preview.Employees.Add(row);
        }

        var unmatched = transactions.Where(t => t.EmployeeId < 0).ToList();

        if (unmatched.Count > 0)
        {
            var badges = unmatched.Select(u => string.IsNullOrWhiteSpace(u.Badge) ? "(blank)" : u.Badge)
                .Distinct().Take(10);
            preview.Warnings.Add(
                $"{unmatched.Count} punch(es) reference a badge that is not in the Employees table " +
                $"(badges: {string.Join(", ", badges)}). They will be skipped.");
        }

        if (transactions.Count == 0)
            preview.Warnings.Add("No punches were found in this database.");

        if (preview.MiscHourTransactions > 0)
            preview.Warnings.Add(
                $"{preview.MiscHourTransactions} transaction(s) carry an hours value " +
                $"({preview.MiscHoursTotal:0.##} hours total) - typically vacation, holiday or sick time. " +
                "They are excluded from punch pairing. Enable \"import paid hours\" to bring them in.");

        if (options.DirectionMode == PunchDirectionMode.Auto && detection.Confidence < 50 && transactions.Count > 0)
            preview.Warnings.Add(
                "The stored IN/OUT codes could not be identified with confidence, so punches were " +
                "alternated (IN, OUT, IN, OUT). Check the first few days on the review screen.");

        return preview;
    }

    private static string ShortKind(PunchKind kind) => kind switch
    {
        PunchKind.In => "IN",
        PunchKind.Out => "OUT",
        PunchKind.BreakOut => "brk",
        PunchKind.BreakIn => "back",
        _ => "hrs",
    };

    private static int SafeCount(Jet4Database db, string table)
    {
        try { return db.HasTable(table) ? db.GetTable(table).CountRows() : 0; }
        catch { return 0; }
    }

    // =================================================================
    // Import
    // =================================================================

    public ImportResult Import(Storage.Store store, ImportOptions? options = null,
        Action<int, int, string>? progress = null)
    {
        options ??= new ImportOptions();
        var started = DateTime.UtcNow;
        var result = new ImportResult();

        using var db = Jet4Database.Open(_path);

        var legacyEmployees = ReadEmployees(db);
        var transactions = ReadTransactions(db);

        if (options.FromDate is not null)
            transactions = transactions.Where(t => t.Timestamp >= options.FromDate.Value).ToList();
        if (options.ToDate is not null)
            transactions = transactions.Where(t => t.Timestamp < options.ToDate.Value.Date.AddDays(1)).ToList();

        var lookup = BuildEmployeeLookup(legacyEmployees);
        var timeCardMap = BuildTimeCardEmployeeMap(db);
        var known = legacyEmployees.Where(e => e.Employee.LegacyId is not null)
            .Select(e => e.Employee.LegacyId!.Value).ToHashSet();
        ResolveEmployees(transactions, lookup, timeCardMap, known);

        var detection = PunchDirectionDetector.Detect(transactions, options.DirectionMode);
        PunchDirectionDetector.Assign(transactions, detection);

        store.Db.InTransaction(_ =>
        {
            // --- shifts -------------------------------------------------
            var shiftMap = new Dictionary<int, int>(); // legacy shift id -> new id
            if (options.ImportShifts && db.HasTable("Shifts"))
            {
                foreach (var row in db.GetTable("Shifts").Rows())
                {
                    int legacyId = row.GetInt32("ShiftID") ?? 0;
                    if (legacyId == 0) continue;
                    if (row.GetBoolean("ShiftEnabled") != true) continue; // unused placeholder shift

                    var shift = MapShift(row);
                    var existing = store.FindShiftByLegacyId(legacyId);
                    if (existing is not null)
                    {
                        shift.Id = existing.Id;
                        shift.LegacyId = legacyId;
                        store.SaveShift(shift);
                    }
                    else
                    {
                        shift.LegacyId = legacyId;
                        store.SaveShift(shift);
                        result.ShiftsAdded++;
                    }
                    shiftMap[legacyId] = shift.Id;
                }
            }

            // --- employees ----------------------------------------------
            var employeeMap = new Dictionary<int, Employee>();

            foreach (var legacy in legacyEmployees)
            {
                int legacyId = legacy.Employee.LegacyId ?? -1;

                Employee? existing = options.MatchExistingEmployees
                    ? store.FindByLegacyId(legacyId)
                      ?? (string.IsNullOrWhiteSpace(legacy.Employee.Badge) ? null : store.FindByBadge(legacy.Employee.Badge))
                      ?? (string.IsNullOrWhiteSpace(legacy.Employee.Pin) ? null : store.FindByPin(legacy.Employee.Pin))
                    : null;

                if (existing is not null && !options.ImportEmployees)
                {
                    employeeMap[legacyId] = existing;
                    continue;
                }

                if (!options.ImportEmployees)
                    continue;

                var mapped = MapEmployee(legacy, shiftMap);

                if (existing is not null)
                {
                    mapped.Id = existing.Id;
                    mapped.LegacyId = existing.LegacyId ?? legacyId;
                    if (existing.Pin.Length > 0 && mapped.Pin.Length == 0) mapped.Pin = existing.Pin;
                    if (existing.Badge.Length > 0 && mapped.Badge.Length == 0) mapped.Badge = existing.Badge;
                    store.UpdateEmployee(mapped);
                    result.EmployeesUpdated++;
                    employeeMap[legacyId] = mapped;
                }
                else
                {
                    // Two legacy employees can share a badge or PIN. The new database
                    // requires both to be unique, so resolve the clash instead of failing
                    // the whole import.
                    if (string.IsNullOrWhiteSpace(mapped.Pin))
                    {
                        mapped.Pin = MakeUniquePin(store, mapped);
                    }
                    else if (store.FindByPin(mapped.Pin) is not null)
                    {
                        result.Warnings.Add(
                            $"{mapped.DisplayName}: PIN {mapped.Pin} is already in use; a new PIN was assigned.");
                        mapped.Pin = MakeUniquePin(store, mapped);
                    }

                    if (mapped.Badge.Length > 0 && store.FindByBadge(mapped.Badge) is not null)
                    {
                        result.Warnings.Add(
                            $"{mapped.DisplayName}: badge {mapped.Badge} is already in use; it was cleared.");
                        mapped.Badge = "";
                    }

                    store.InsertEmployee(mapped);
                    result.EmployeesAdded++;
                    employeeMap[legacyId] = mapped;
                }
            }

            // --- pay periods --------------------------------------------
            var importedPeriods = new List<(DateTime Start, DateTime End, PayPeriodType Type)>();
            if (options.ImportPayPeriods && db.HasTable("PayPeriods"))
            {
                foreach (var row in db.GetTable("PayPeriods").Rows())
                {
                    var start = row.GetDateTime("PeriodStart");
                    var end = row.GetDateTime("PeriodEnd");
                    if (start is null || end is null || end < start) continue;
                    if ((end.Value.Date - start.Value.Date).TotalDays > 45) continue; // placeholder period

                    var period = new PayPeriod
                    {
                        Start = start.Value.Date,
                        End = end.Value.Date,
                        Type = InferPeriodType(start.Value, end.Value),
                    };
                    store.UpsertPayPeriod(period);
                    importedPeriods.Add((period.Start, period.End, period.Type));
                    result.PayPeriodsAdded++;
                }
            }

            // --- punches ------------------------------------------------
            if (options.ImportPunches)
            {
                var seen = new HashSet<(int, DateTime, PunchKind)>();
                int total = transactions.Count;
                int index = 0;

                foreach (var transaction in transactions)
                {
                    index++;
                    if (index % 250 == 0)
                        progress?.Invoke(index, total, $"{index:n0} of {total:n0} punches");

                    int legacyEmployeeId = transaction.EmployeeId;
                    if (legacyEmployeeId < 0 || !employeeMap.TryGetValue(legacyEmployeeId, out var employee))
                    {
                        result.UnmatchedPunches++;
                        continue;
                    }

                    if (transaction.Hours > 0 && !options.ImportMiscHours)
                    {
                        result.PunchesSkipped++;
                        continue;
                    }

                    // Guard against importing the same punch twice, whether on a re-run or
                    // from an overlapping archive snapshot.
                    if (store.HasPunch(employee.Id, transaction.Timestamp, transaction.Kind))
                    {
                        result.PunchesSkipped++;
                        continue;
                    }
                    if (transaction.LegacyId > 0 && store.FindPunchByLegacyId(transaction.LegacyId) is not null)
                    {
                        result.PunchesSkipped++;
                        continue;
                    }
                    if (!seen.Add((employee.Id, transaction.Timestamp, transaction.Kind)))
                    {
                        result.PunchesSkipped++;
                        continue;
                    }

                    var period = store.GetPayPeriodFor(transaction.Timestamp);
                    store.InsertPunch(new Punch
                    {
                        EmployeeId = employee.Id,
                        PayPeriodId = period?.Id,
                        Timestamp = transaction.Timestamp,
                        Kind = transaction.Kind,
                        Source = PunchSource.Import,
                        Comment = transaction.Comment,
                        LegacyId = transaction.LegacyId,
                        Hours = TimeSpan.FromMinutes(transaction.Hours),
                        HoursCategory = transaction.Hours > 0 ? "Other" : "",
                    });
                    result.PunchesAdded++;
                }
            }

            // --- holidays -----------------------------------------------
            if (options.ImportHolidays && db.HasTable("Holidays"))
            {
                foreach (var row in db.GetTable("Holidays").Rows())
                {
                    var date = row.GetDateTime("HolidayDate");
                    if (date is null) continue;
                    store.SaveHoliday(new Holiday
                    {
                        Name = row.GetString("HolidayName") ?? "Holiday",
                        Date = date.Value.Date,
                        Paid = true,
                    });
                    result.HolidaysAdded++;
                }
            }

            // --- optionally deactivate people with no recent punches ----
            if (options.InactiveAfterDaysWithoutPunch > 0 && employeeMap.Count > 0)
            {
                var cutoff = DateTime.Now.AddDays(-options.InactiveAfterDaysWithoutPunch);
                var lastPunch = transactions
                    .GroupBy(t => t.Badge, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Max(t => t.Timestamp),
                        StringComparer.OrdinalIgnoreCase);

                foreach (var pair in employeeMap)
                {
                    bool recent = (pair.Value.Badge.Length > 0 &&
                                   lastPunch.TryGetValue(pair.Value.Badge, out var when) && when >= cutoff);
                    if (recent || pair.Value.Status == EmployeeStatus.Inactive) continue;

                    pair.Value.Status = EmployeeStatus.Inactive;
                    store.UpdateEmployee(pair.Value);
                }
            }

            // --- make sure every imported punch belongs to a period ------
            if (transactions.Count > 0)
            {
                var min = transactions.Min(t => t.Timestamp);
                var max = transactions.Max(t => t.Timestamp);

                // Follow the legacy pay period rhythm when there is one, so the new
                // periods line up exactly with the old payroll periods.
                var settingsNow = store.GetSettings();
                if (importedPeriods.Count > 0)
                {
                    var mostCommon = importedPeriods
                        .GroupBy(p => p.Type)
                        .OrderByDescending(g => g.Count())
                        .First().Key;
                    settingsNow.PeriodType = mostCommon;
                    settingsNow.PeriodAnchor = importedPeriods.Min(p => p.Start);
                }

                foreach (var generated in TimeCalc.PayPeriodGenerator.EnsureRange(settingsNow, min, max))
                    store.UpsertPayPeriod(generated);

                store.Db.Execute("""
                    UPDATE punches SET pay_period_id = (
                        SELECT id FROM pay_periods
                        WHERE start_date <= date(punches.timestamp)
                          AND end_date   >= date(punches.timestamp)
                        ORDER BY start_date DESC LIMIT 1)
                    WHERE pay_period_id IS NULL;
                    """);
            }

            // --- settings -----------------------------------------------
            if (options.ImportSettings && db.HasTable("Setup"))
            {
                var row = db.GetTable("Setup").Rows().FirstOrDefault();
                if (row is not null)
                {
                    var settings = store.GetSettings();
                    ApplyLegacySetup(row, settings);
                    if (importedPeriods.Count > 0)
                    {
                        settings.PeriodType = importedPeriods
                            .GroupBy(p => p.Type).OrderByDescending(g => g.Count()).First().Key;
                        settings.PeriodAnchor = importedPeriods.Min(p => p.Start);
                        settings.SemiMonthlySplitDay = settings.SemiMonthlySplitDay;
                    }
                    store.SaveSettings(settings);
                }
            }

            // --- approvals ----------------------------------------------
            if (options.ImportApprovals && db.HasTable("TimeCards") && employeeMap.Count > 0)
            {
                var legacyPeriods = ReadLegacyPeriods(db);
                foreach (var row in db.GetTable("TimeCards").Rows())
                {
                    if (row.GetBoolean("Approved") != true) continue;

                    int legacyEmployeeId = row.GetInt32("EmployeeID") ?? -1;
                    int legacyPeriodId = row.GetInt32("PayPeriodID") ?? -1;
                    if (!employeeMap.TryGetValue(legacyEmployeeId, out var employee)) continue;
                    if (!legacyPeriods.TryGetValue(legacyPeriodId, out var range)) continue;

                    var stored = store.GetPayPeriodFor(range.Start);
                    if (stored is null) continue;

                    var card = store.GetOrCreateTimeCard(employee.Id, stored.Id);
                    card.Approved = true;
                    card.ApprovedBy = row.GetString("ApprovedSuper") ?? "";
                    card.ApprovedAt = row.GetDateTime("ApprovedDate");
                    store.SaveTimeCard(card);
                    result.ApprovalsImported++;
                }
            }

            store.LogImport("timeQplus v3 (arx_active.mdb)",
                $"{result.EmployeesAdded} employees, {result.PunchesAdded} punches",
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    source = Path.GetFullPath(_path),
                    employees = result.EmployeesAdded,
                    updated = result.EmployeesUpdated,
                    punches = result.PunchesAdded,
                    skipped = result.PunchesSkipped,
                    unmatched = result.UnmatchedPunches,
                    direction = detection.Description,
                    confidence = detection.Confidence,
                    from = options.FromDate,
                    to = options.ToDate,
                }));
        });

        result.Duration = DateTime.UtcNow - started;
        return result;
    }

    /// <summary>
    /// timeQplus links a punch to an employee through TimeCards: Transactions.TimeCardID
    /// -> TimeCards.EmployeeID. On the PC clock the TransBadge column is left empty, so
    /// this link is the only reliable way to attribute a punch.
    /// </summary>
    internal static Dictionary<int, int> BuildTimeCardEmployeeMap(Jet4Database db)
    {
        var map = new Dictionary<int, int>();
        if (!db.HasTable("TimeCards")) return map;

        try
        {
            foreach (var row in db.GetTable("TimeCards").Rows())
            {
                int timeCardId = row.GetInt32("TimeCardID") ?? -1;
                int employeeId = row.GetInt32("EmployeeID") ?? -1;
                if (timeCardId > 0 && employeeId > 0)
                    map[timeCardId] = employeeId;
            }
        }
        catch
        {
            // A damaged TimeCards table must not stop the import; badge matching still works.
        }

        return map;
    }

    internal static void ResolveEmployees(List<LegacyTransaction> transactions,
        Dictionary<string, int> lookup, Dictionary<int, int> timeCardMap,
        HashSet<int> knownEmployees)
    {
        foreach (var transaction in transactions)
        {
            int id = -1;

            if (transaction.TimeCardId is int cardId &&
                timeCardMap.TryGetValue(cardId, out int fromCard) &&
                knownEmployees.Contains(fromCard))
            {
                id = fromCard;
            }
            else if (ResolveEmployeeId(transaction, lookup) is int fromBadge && fromBadge >= 0)
            {
                id = fromBadge;
            }

            transaction.EmployeeId = id;
        }
    }

    private static Dictionary<int, (DateTime Start, DateTime End)> ReadLegacyPeriods(Jet4Database db)
    {
        var map = new Dictionary<int, (DateTime, DateTime)>();
        if (!db.HasTable("PayPeriods")) return map;

        foreach (var row in db.GetTable("PayPeriods").Rows())
        {
            int id = row.GetInt32("PayPeriodID") ?? -1;
            var start = row.GetDateTime("PeriodStart");
            var end = row.GetDateTime("PeriodEnd");
            if (id > 0 && start is not null && end is not null)
                map[id] = (start.Value, end.Value);
        }
        return map;
    }

    // =================================================================
    // Reading legacy rows
    // =================================================================

    internal static List<LegacyEmployee> ReadEmployees(Jet4Database db)
    {
        var list = new List<LegacyEmployee>();
        if (!db.HasTable("Employees")) return list;

        foreach (var row in db.GetTable("Employees").Rows())
        {
            int legacyId = row.GetInt32("EmployeeID") ?? 0;
            if (legacyId == 0) continue;

            int status = row.GetInt32("Status") ?? 0;

            list.Add(new LegacyEmployee
            {
                LegacyStatus = status,
                LegacyShiftId = row.GetInt32("Shift"),
                Employee = new Employee
                {
                    LegacyId = legacyId,
                    FirstName = Clean(row.GetString("Firstname")),
                    MiddleInitial = Clean(row.GetString("MI")),
                    LastName = Clean(row.GetString("Lastname")),
                    Badge = Clean(row.GetString("Badge")),
                    Pin = Clean(row.GetString("PIN")),
                    EmployeeNumber = Clean(row.GetString("Number")),
                    Message = Clean(row.GetString("Message")),
                    HireDate = SaneDate(row.GetDateTime("HireDate")),
                    ExportId = Clean(row.GetString("ExportID")),
                    Status = status == 2 ? EmployeeStatus.Inactive : EmployeeStatus.Active,
                    Custom1 = Clean(row.GetString("Custom1")),
                    Custom2 = Clean(row.GetString("Custom2")),
                    Custom3 = Clean(row.GetString("Custom3")),
                    Custom4 = Clean(row.GetString("Custom4")),
                    Custom5 = Clean(row.GetString("Custom5")),
                    Notes = $"Imported from timeQplus (legacy status {status})",
                },
            });
        }

        return list;
    }

    private static string Clean(string? value) => (value ?? "").Trim();

    internal static List<LegacyTransaction> ReadTransactions(Jet4Database db)
    {
        var list = new List<LegacyTransaction>();
        if (!db.HasTable("Transactions")) return list;

        foreach (var row in db.GetTable("Transactions").Rows())
        {
            var when = row.GetDateTime("TransDateTime");
            if (when is null) continue;

            list.Add(new LegacyTransaction
            {
                LegacyId = row.GetInt32("TransactionID") ?? 0,
                Timestamp = when.Value,
                Badge = Clean(row.GetString("TransBadge")),
                Type = row.GetInt32("TransType") ?? 0,
                Source = row.GetInt32("TransSource") ?? 0,
                State = row.GetInt32("TransState") ?? 0,
                Hours = row.GetInt32("TransHours") ?? 0,
                Comment = Clean(row.GetString("Comment")),
                TimeCardId = row.GetInt32("TimeCardID"),
            });
        }

        list.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return list;
    }

    internal static Dictionary<string, int> BuildEmployeeLookup(List<LegacyEmployee> employees)
    {
        var lookup = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var legacy in employees)
        {
            if (legacy.Employee.LegacyId is not int id) continue;

            void Add(string? key)
            {
                if (string.IsNullOrWhiteSpace(key)) return;
                string trimmed = key.Trim();
                lookup.TryAdd(trimmed, id);

                // Also match loosely: badges are often stored as 0012 in one place and
                // 12 in another, and terminals sometimes pad values with spaces.
                string normalized = Normalize(trimmed);
                if (normalized.Length > 0)
                    lookup.TryAdd("#" + normalized, id);
            }

            Add(legacy.Employee.Badge);
            Add(legacy.Employee.Pin);
            Add(legacy.Employee.EmployeeNumber);
            Add(id.ToString());
        }

        return lookup;
    }

    internal static int ResolveEmployeeId(LegacyTransaction transaction, Dictionary<string, int> lookup)
    {
        if (string.IsNullOrWhiteSpace(transaction.Badge))
            return -1;

        string badge = transaction.Badge.Trim();

        if (lookup.TryGetValue(badge, out int id))
            return id;

        string normalized = Normalize(badge);
        if (normalized.Length > 0 && lookup.TryGetValue("#" + normalized, out id))
            return id;

        return -1;
    }

    /// <summary>Lower-cases and strips leading zeros so 0012 and 12 are the same badge.</summary>
    internal static string Normalize(string value)
    {
        value = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (value.Length == 0) return "";

        if (value.All(char.IsDigit))
        {
            string trimmed = value.TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        return value.ToLowerInvariant();
    }

    // =================================================================
    // Mapping helpers
    // =================================================================

    private static Shift MapShift(Jet4Row row)
    {
        var start = row.GetDateTime("ShiftStart") ?? DateTime.Today.AddHours(8);
        var stop = row.GetDateTime("ShiftStop") ?? DateTime.Today.AddHours(17);

        return new Shift
        {
            Name = row.GetString("ShiftName") is { Length: > 0 } name ? name : $"Shift {row.GetInt32("ShiftNumber") ?? 0}",
            Enabled = row.GetBoolean("ShiftEnabled") ?? true,
            Start = start.TimeOfDay,
            Stop = stop.TimeOfDay,
            StartGraceMinutes = SaneMinutes(row.GetInt32("StartGrace")),
            StopGraceMinutes = SaneMinutes(row.GetInt32("StopGrace")),
            DockLateArrival = (row.GetInt32("StartDock") ?? 0) > 0,
            DockEarlyLeave = (row.GetInt32("StopDock") ?? 0) > 0,
            LunchEnabled = row.GetBoolean("LunchEnabled") ?? false,
            LunchStart = (row.GetDateTime("LunchStart") ?? DateTime.Today.AddHours(12)).TimeOfDay,
            LunchEnd = (row.GetDateTime("LunchEnd") ?? DateTime.Today.AddHours(12.5)).TimeOfDay,
            LunchDurationMinutes = SaneMinutes(row.GetInt32("LunchDuration"), 30),
            LunchAutoAfterMinutes = SaneMinutes(row.GetInt32("LunchAutoAfter")),
            HolidaysPaid = row.GetBoolean("HolidaysPaid") ?? false,
            HolidayHours = row.GetInt32("HolidayHours") ?? 0,
            LegacyId = row.GetInt32("ShiftID"),
        };
    }

    private static int SaneMinutes(int? value, int fallback = 0)
    {
        if (value is null || value < 0 || value > 24 * 60) return fallback;
        return value.Value;
    }

    private static Employee MapEmployee(LegacyEmployee legacy, Dictionary<int, int> shiftMap)
    {
        var employee = new Employee
        {
            FirstName = legacy.Employee.FirstName,
            MiddleInitial = legacy.Employee.MiddleInitial,
            LastName = legacy.Employee.LastName,
            Badge = legacy.Employee.Badge,
            Pin = legacy.Employee.Pin,
            EmployeeNumber = legacy.Employee.EmployeeNumber,
            Message = legacy.Employee.Message,
            HireDate = legacy.Employee.HireDate,
            ExportId = legacy.Employee.ExportId,
            Status = legacy.Employee.Status,
            Custom1 = legacy.Employee.Custom1,
            Custom2 = legacy.Employee.Custom2,
            Custom3 = legacy.Employee.Custom3,
            Custom4 = legacy.Employee.Custom4,
            Custom5 = legacy.Employee.Custom5,
            LegacyId = legacy.Employee.LegacyId,
            Notes = legacy.Employee.Notes,
        };

        if (legacy.LegacyShiftId is int legacyShift && shiftMap.TryGetValue(legacyShift, out int newShift))
            employee.ShiftId = newShift;

        return employee;
    }

    private static string MakeUniquePin(Storage.Store store, Employee employee)
    {
        foreach (var candidate in new[] { employee.Badge, employee.EmployeeNumber, employee.LegacyId?.ToString() })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (store.FindByPin(candidate) is null) return candidate;
        }

        for (int i = 1000; i < 100_000; i++)
        {
            string candidate = i.ToString();
            if (store.FindByPin(candidate) is null) return candidate;
        }

        return Guid.NewGuid().ToString("n")[..6];
    }

    internal static PayPeriodType InferPeriodType(DateTime start, DateTime end)
    {
        int days = (int)(end.Date - start.Date).TotalDays + 1;
        return days switch
        {
            <= 7 => PayPeriodType.Weekly,
            <= 14 => PayPeriodType.BiWeekly,
            <= 17 => PayPeriodType.SemiMonthly,
            _ => PayPeriodType.Monthly,
        };
    }

    private static void ApplyLegacySetup(Jet4Row row, AppSettings settings)
    {
        // Thresholds in timeQplus are minutes. Only values in a plausible range are
        // imported so that a different unit can never silently corrupt the setup.
        settings.CompanyMessage = row.GetString("CompanyMessage") ?? settings.CompanyMessage;
        settings.RoundingMinutes = SaneRounding(row.GetInt32("PayIntervalRound"));
        settings.DailyOverTime1Minutes = SaneDailyThreshold(row.GetInt32("DailyOT1Thresh"));
        settings.DailyOverTime2Minutes = SaneDailyThreshold(row.GetInt32("DailyOT2Thresh"));
        settings.WeeklyOverTime1Minutes = SaneWeeklyThreshold(row.GetInt32("WeeklyOT1Thresh"));
        settings.WeeklyOverTime2Minutes = SaneWeeklyThreshold(row.GetInt32("WeeklyOT2Thresh"));
        settings.DayChangeOffsetMinutes = SaneMinutes(row.GetInt32("DayChangeOffset"));

        int maxOnClock = SaneMinutes(row.GetInt32("MaxTimeOnClock"));
        if (maxOnClock >= 60) settings.MaxPunchSpanMinutes = maxOnClock;
    }

    /// <summary>
    /// timeQplus disables a daily overtime band by setting it to 24 hours (1440 minutes).
    /// Weekly bands are disabled at 168 hours (10080 minutes).
    /// </summary>
    private static int SaneDailyThreshold(int? value)
    {
        if (value is null || value < 0 || value >= 24 * 60) return 0;
        return value.Value;
    }

    private static int SaneWeeklyThreshold(int? value)
    {
        if (value is null || value < 0 || value >= 7 * 24 * 60) return 0;
        return value.Value;
    }

    /// <summary>
    /// PayIntervalRound is an index/enum in the legacy schema, not a number of minutes.
    /// Only the unambiguous, common payroll intervals are imported.
    /// </summary>
    private static int SaneRounding(int? value)
    {
        if (value is null) return 0;
        return value.Value is 5 or 6 or 10 or 15 or 20 or 30 or 60 ? value.Value : 0;
    }

    /// <summary>Access stores "no date" as the 1899 epoch; treat anything before 1900 as empty.</summary>
    private static DateTime? SaneDate(DateTime? value)
    {
        if (value is null) return null;
        return value.Value.Year <= 1900 ? null : value;
    }
}

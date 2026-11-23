using PunchPoint.Core.Jet;

namespace PunchPoint.Core.Tests;

public class Jet4ReaderTests
{
    private static string FixturePath => Path.Combine(TestPaths.Fixtures, "legacy-timeqplus.mdb");

    [Fact]
    public void Opens_jet4_database_and_lists_tables()
    {
        using var db = Jet4Database.Open(FixturePath);

        Assert.Contains("Employees", db.TableNames);
        Assert.Contains("Transactions", db.TableNames);
        Assert.Contains("Shifts", db.TableNames);
        Assert.Contains("PayPeriods", db.TableNames);
        Assert.Contains("Setup", db.TableNames);
    }

    [Fact]
    public void Reads_all_employee_rows_with_correct_values()
    {
        using var db = Jet4Database.Open(FixturePath);
        var table = db.GetTable("Employees");
        var rows = table.Rows().ToList();

        Assert.Equal(6, rows.Count);

        var maria = rows.Single(r => r.GetInt32("EmployeeID") == 1);
        Assert.Equal("Maria", maria.GetString("Firstname"));
        Assert.Equal("Gonzalez", maria.GetString("Lastname"));
        Assert.Equal("L", maria.GetString("MI"));
        Assert.Equal("1001", maria.GetString("Badge"));
        Assert.Equal("A-001", maria.GetString("Number"));
        Assert.Equal(2, maria.GetInt32("Shift"));
        Assert.Equal(new DateTime(2021, 3, 15), maria.GetDateTime("HireDate")!.Value.Date);
    }

    [Fact]
    public void Reads_boolean_columns_from_the_null_mask()
    {
        using var db = Jet4Database.Open(FixturePath);
        var shift = db.GetTable("Shifts").Rows().First(r => r.GetInt32("ShiftID") == 1);

        Assert.True(shift.GetBoolean("ShiftEnabled"));
        Assert.True(shift.GetBoolean("LunchEnabled"));
        Assert.True(shift.GetBoolean("HolidaysPaid"));
    }

    [Fact]
    public void Reads_memo_columns()
    {
        using var db = Jet4Database.Open(FixturePath);

        var vacation = db.GetTable("Transactions").Rows()
            .Single(r => r.GetString("Comment") is { Length: > 0 });

        Assert.Equal("Vacation day", vacation.GetString("Comment"));
    }

    [Fact]
    public void Reads_datetime_columns_to_the_second()
    {
        using var db = Jet4Database.Open(FixturePath);
        var first = db.GetTable("Transactions").Rows()
            .OrderBy(r => r.GetDateTime("TransDateTime"))
            .First();

        Assert.Equal(new DateTime(2026, 9, 1, 8, 0, 0), first.GetDateTime("TransDateTime"));
    }

    [Fact]
    public void Rejects_non_access_files()
    {
        string path = Path.Combine(Path.GetTempPath(), $"not-a-db-{Guid.NewGuid():n}.mdb");
        File.WriteAllText(path, "hello");
        try
        {
            Assert.Throws<Jet4FormatException>(() => Jet4Database.Open(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Row_count_matches_definition_and_actual_rows()
    {
        using var db = Jet4Database.Open(FixturePath);
        var table = db.GetTable("Transactions");
        Assert.Equal(table.Rows().Count(), table.CountRows());
        Assert.Equal(86, table.CountRows());
    }
}

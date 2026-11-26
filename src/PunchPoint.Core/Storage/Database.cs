using System.Data;
using Microsoft.Data.Sqlite;

namespace PunchPoint.Core.Storage;

public static class AppPaths
{
    /// <summary>
    /// Writable data folder. On Windows this is %ProgramData%\PunchPoint (all users);
    /// on other platforms (used for development and tests) it is in the user profile.
    /// </summary>
    public static string DataDirectory => Environment.GetEnvironmentVariable("PUNCHPOINT_DATA")
        ?? (OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "PunchPoint")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "PunchPoint"));

    public static string DatabasePath => Path.Combine(DataDirectory, "punchpoint.db");

    public static string BackupsDirectory => Path.Combine(DataDirectory, "Backups");

    public static string LogsDirectory => Path.Combine(DataDirectory, "Logs");

    public static string EnsureDataDirectory()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        return DataDirectory;
    }
}

/// <summary>
/// Thin SQLite wrapper: connection management, schema creation and small helpers.
/// </summary>
public sealed class Database : IDisposable
{
    private readonly SqliteConnection _connection;

    private Database(SqliteConnection connection, string path)
    {
        _connection = connection;
        Path = path;
    }

    public string Path { get; }
    public SqliteConnection Connection => _connection;

    public static Database OpenDefault() => Open(AppPaths.DatabasePath);

    public static Database Open(string path)
    {
        string? dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        var db = new Database(connection, path);
        db.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;");
        Schema.Ensure(db);
        return db;
    }

    // ---- helpers -------------------------------------------------------

    public SqliteCommand Command(string sql, params (string Name, object? Value)[] parameters)
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    public int Execute(string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(sql, parameters);
        return cmd.ExecuteNonQuery();
    }

    public object? Scalar(string sql, params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(sql, parameters);
        var result = cmd.ExecuteScalar();
        return result is DBNull ? null : result;
    }

    public List<T> Query<T>(string sql, Func<SqliteDataReader, T> map,
        params (string Name, object? Value)[] parameters)
    {
        using var cmd = Command(sql, parameters);
        using var reader = cmd.ExecuteReader();
        var list = new List<T>();
        while (reader.Read()) list.Add(map(reader));
        return list;
    }

    public void InTransaction(Action<SqliteConnection> action)
    {
        using var tx = _connection.BeginTransaction();
        try
        {
            action(_connection);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>Copies the database (and its WAL) to a timestamped backup file.</summary>
    /// <summary>
    /// Writes a consistent copy of the database. <paramref name="timestamp"/> lets the caller
    /// name the file with the same clock it uses for the "one backup per day" check.
    /// </summary>
    public string Backup(string? destinationDirectory = null, DateTime? timestamp = null)
    {
        string dir = destinationDirectory ?? AppPaths.BackupsDirectory;
        Directory.CreateDirectory(dir);

        string baseName = $"{System.IO.Path.GetFileNameWithoutExtension(Path)}-{(timestamp ?? DateTime.Now):yyyyMMdd-HHmmss}";
        string target = System.IO.Path.Combine(dir, baseName + ".db");

        // Two launches inside the same second must not collide.
        int suffix = 1;
        while (File.Exists(target))
            target = System.IO.Path.Combine(dir, $"{baseName}-{suffix++}.db");

        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "VACUUM INTO $target";
            cmd.Parameters.AddWithValue("$target", target);
            cmd.ExecuteNonQuery();
        }

        return target;
    }

    public void Dispose() => _connection.Dispose();
}

public static class DbExtensions
{
    public static string? GetStringOrNull(this SqliteDataReader r, string name)
    {
        int i = r.GetOrdinal(name);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static int GetInt(this SqliteDataReader r, string name, int fallback = 0)
    {
        int i = r.GetOrdinal(name);
        return r.IsDBNull(i) ? fallback : Convert.ToInt32(r.GetValue(i));
    }

    public static int? GetIntOrNull(this SqliteDataReader r, string name)
    {
        int i = r.GetOrdinal(name);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }

    public static bool GetBool(this SqliteDataReader r, string name, bool fallback = false)
    {
        int i = r.GetOrdinal(name);
        return r.IsDBNull(i) ? fallback : Convert.ToInt64(r.GetValue(i)) != 0;
    }

    public static decimal? GetDecimalOrNull(this SqliteDataReader r, string name)
    {
        int i = r.GetOrdinal(name);
        return r.IsDBNull(i) ? null : Convert.ToDecimal(r.GetValue(i));
    }

    public static DateTime? GetDateOrNull(this SqliteDataReader r, string name)
    {
        string? s = r.GetStringOrNull(name);
        return s is null ? null : DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static DateTime GetDate(this SqliteDataReader r, string name)
        => r.GetDateOrNull(name) ?? DateTime.MinValue;
}

namespace PunchPoint.Core.Jet;

/// <summary>
/// One decoded row. Values are exposed as .NET primitives:
/// string for Text/Memo, int for Int32, short for Int16, DateTime for DateTime,
/// bool for Boolean, double/float/decimal for floating point/currency.
/// </summary>
public sealed class Jet4Row
{
    private readonly Dictionary<string, object?> _values;

    internal Jet4Row(Dictionary<string, object?> values, int rowNumber, int pageNumber)
    {
        _values = values;
        RowNumber = rowNumber;
        PageNumber = pageNumber;
    }

    public int RowNumber { get; }
    public int PageNumber { get; }

    public IReadOnlyDictionary<string, object?> Values => _values;

    public object? this[string column] =>
        _values.TryGetValue(column, out var v) ? v : null;

    public bool Has(string column) => _values.ContainsKey(column);

    public string? GetString(string column) => this[column] switch
    {
        null => null,
        string s => s,
        byte[] b => System.Text.Encoding.Unicode.GetString(b),
        var v => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
    };

    public bool TryGetInt32(string column, out int value)
    {
        switch (this[column])
        {
            case int i: value = i; return true;
            case short s: value = s; return true;
            case byte b: value = b; return true;
            case bool b: value = b ? 1 : 0; return true;
            case long l: value = (int)l; return true;
            case double d: value = (int)d; return true;
            case string s when int.TryParse(s, out int p): value = p; return true;
            default: value = 0; return false;
        }
    }

    public int? GetInt32(string column) =>
        TryGetInt32(column, out int v) ? v : null;

    public bool? GetBoolean(string column) => this[column] switch
    {
        bool b => b,
        int i => i != 0,
        short s => s != 0,
        _ => null,
    };

    public DateTime? GetDateTime(string column) => this[column] switch
    {
        DateTime dt => dt,
        double d => JetDates.FromOleAutomation(d),
        int i => JetDates.FromOleAutomation(i),
        _ => null,
    };

    public override string ToString() =>
        string.Join(", ", _values.Select(kv => $"{kv.Key}={kv.Value ?? "NULL"}"));
}

public static class JetDates
{
    /// <summary>Access/OLE Automation date epoch: 30 December 1899.</summary>
    public static readonly DateTime Epoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    public static DateTime FromOleAutomation(double days)
    {
        // Access Date/Time values have one-second resolution. The stored double can
        // land a fraction below the intended second (e.g. 23:41:27.9999), which Access
        // itself displays as 23:41:28, so round to the nearest second.
        var raw = Epoch.AddDays(days);
        long ticks = (long)Math.Round(raw.Ticks / (double)TimeSpan.TicksPerSecond) * TimeSpan.TicksPerSecond;
        if (ticks < DateTime.MinValue.Ticks) ticks = DateTime.MinValue.Ticks;
        if (ticks > DateTime.MaxValue.Ticks) ticks = DateTime.MaxValue.Ticks;
        return new DateTime(ticks, DateTimeKind.Unspecified);
    }

    public static DateTime? FromOleAutomation(double? days) =>
        days is null ? null : FromOleAutomation(days.Value);
}

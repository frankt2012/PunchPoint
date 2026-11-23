
public static class ReaderExtensions
{
    /// <summary>True when the result set contains the given column (older schemas lack newer columns).</summary>
    public static bool HasColumn(this Microsoft.Data.Sqlite.SqliteDataReader reader, string name)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
